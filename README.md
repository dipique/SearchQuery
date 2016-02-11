# SearchQuery
Tool that allows low-overhead multiple-input searches with dynamically construct LINQ expressions

Contains an inheritable class that contains the logic to apply a set of filters to a dataset based
on a set of provided conditions.  The values in the derived class are mapped to the object
fields using reflection and attributes on the derived class fields.

To use this object:
(1) Create a derived class that contains all the fields that will be entered as search
    criteria.
(2) Use the attributes defined below to link those fields to the target object fields/
    properties. 
(3) Create a constructor that specifies the return type
(4) Create an instance of the inherited class, populate the search fields, and call the GetResults()
    method on the dataset.
