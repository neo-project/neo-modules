# StateService

## RPC API

### GetStateRoot
#### Params
|Name|Type|Summary|Required|
|-|-|-|-|
|Index|uint|index|true|
#### Result
StateRoot Object
|Name|Type|Summary|
|-|-|-|
|version|number|version|
|index|number|index|
|roothash|string|version|
|witness|Object|witness from validators|

### GetProof
#### Params
|Name|Type|Summary|Required|
|-|-|-|-|
|RootHash|UInt256|state root|true|
|ScriptHash|UInt160|contract script hash|true|
|Key|base64 string|key|true|
#### Result
Proof in base64 string

### VerifyProof
#### Params
|Name|Type|Summary|
|-|-|-|
|RootHash|UInt256|state root|true|
|Proof|base64 string|proof|true|
#### Result
Value in base64 string

### GetStateheight
#### Result
|Name|Type|Summary|
|-|-|-|
|localrootindex|number|root hash index calculated locally|
|validatedrootindex|number|root hash index verified by validators|

### GetState
#### Params
|Name|Type|Summary|Required|
|-|-|-|-|
|Index/BlockHash|uint/UInt256|specify state|true|
|ScriptHash|UInt160|contract script hash|true|
|Key|base64 string|key|true|
|IsFind|bool|treat `Key` as prefix when `true`|optional|
#### Result
* `Get` result
  Value in base64 string or `null`
* `Find` result
    |Name|Type|Summary|
    |-|-|-|
    |array|array|key-value results|
    |truncated|bool|truncated|
